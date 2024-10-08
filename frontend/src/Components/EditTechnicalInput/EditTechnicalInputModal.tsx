import { useEffect, useState } from "react"
import {
    Button,
    Progress,
    Tabs,
} from "@equinor/eds-core-react"
import Grid from "@mui/material/Grid"
import WellCostsTab from "./WellCostsTab"
import PROSPTab from "./PROSPTab"
import { EMPTY_GUID } from "../../Utils/constants"
import { isExplorationWell } from "../../Utils/common"
import CO2Tab from "./CO2Tab"
import { GetTechnicalInputService } from "../../Services/TechnicalInputService"
import { useProjectContext } from "../../Context/ProjectContext"
import { useModalContext } from "../../Context/ModalContext"
import { useAppContext } from "../../Context/AppContext"

const {
    List, Tab, Panels, Panel,
} = Tabs

const EditTechnicalInputModal = () => {
    const { editMode } = useAppContext()
    const [isSaving, setIsSaving] = useState<boolean>(false)
    const { project, setProject } = useProjectContext()
    const {
        technicalModalIsOpen,
        setTechnicalModalIsOpen,
        editTechnicalInput,
        setEditTechnicalInput,
    } = useModalContext()

    const [activeTab, setActiveTab] = useState<number>(0)
    const [deletedWells, setDeletedWells] = useState<string[]>([])

    const [
        explorationOperationalWellCosts,
        setExplorationOperationalWellCosts,
    ] = useState<Components.Schemas.ExplorationOperationalWellCostsDto | undefined>(project?.explorationOperationalWellCosts)
    const [
        developmentOperationalWellCosts,
        setDevelopmentOperationalWellCosts,
    ] = useState<Components.Schemas.DevelopmentOperationalWellCostsDto | undefined>(project?.developmentOperationalWellCosts)

    const [
        originalExplorationOperationalWellCosts,
        setOriginalExplorationOperationalWellCosts,
    ] = useState<Components.Schemas.ExplorationOperationalWellCostsDto | undefined>(project?.explorationOperationalWellCosts)
    const [
        originalDevelopmentOperationalWellCosts,
        setOriginalDevelopmentOperationalWellCosts,
    ] = useState<Components.Schemas.DevelopmentOperationalWellCostsDto | undefined>(project?.developmentOperationalWellCosts)

    const [wellProjectWells, setWellProjectWells] = useState<Components.Schemas.WellDto[]>(project?.wells?.filter((w) => !isExplorationWell(w)) ?? [])
    const [explorationWells, setExplorationWells] = useState<Components.Schemas.WellDto[]>(project?.wells?.filter((w) => isExplorationWell(w)) ?? [])

    const [originalWellProjectWells, setOriginalWellProjectWells] = useState<Components.Schemas.WellDto[]>(project?.wells?.filter((w) => !isExplorationWell(w)) ?? [])
    const [originalExplorationWells, setOriginalExplorationWells] = useState<Components.Schemas.WellDto[]>(project?.wells?.filter((w) => isExplorationWell(w)) ?? [])

    const handleSave = async () => {
        try {
            const dto: Components.Schemas.UpdateTechnicalInputDto = {}
            setIsSaving(true)
            dto.projectDto = { ...project }

            dto.explorationOperationalWellCostsDto = explorationOperationalWellCosts

            dto.developmentOperationalWellCostsDto = developmentOperationalWellCosts

            const wellDtos = [...explorationWells, ...wellProjectWells]

            dto.createWellDtos = wellDtos.filter((w) => w.id === EMPTY_GUID || w.id === undefined || w.id === null || w.id === "")
            dto.updateWellDtos = wellDtos.filter((w) => w.id !== EMPTY_GUID && w.id !== undefined && w.id !== null && w.id !== "")
            dto.deleteWellDtos = deletedWells.map((id) => ({ id }))

            const result = project ? await (await GetTechnicalInputService()).update(project?.id, dto) : undefined

            if (result?.projectDto) {
                setProject({ ...result?.projectDto })
            }

            setOriginalExplorationOperationalWellCosts(explorationOperationalWellCosts)
            setOriginalDevelopmentOperationalWellCosts(developmentOperationalWellCosts)
            setOriginalWellProjectWells([...wellProjectWells])
            setOriginalExplorationWells([...explorationWells])

            // Reset the changes made flag as changes have been successfully saved
            setIsSaving(false) // End saving process
            setEditTechnicalInput(undefined)
        } catch (error) {
            console.error("Error when saving technical input: ", error)
            setIsSaving(false)
        } finally {
            setIsSaving(false)
        }
    }

    const handleSaveAndClose = async () => {
        try {
            await handleSave()
            setTechnicalModalIsOpen(false)
            setEditTechnicalInput(undefined)
        } catch (e) {
            console.error("Error during save operation: ", e)
        }
    }

    const handleCancel = () => {
        // Revert the operational costs to their original state
        setExplorationOperationalWellCosts(originalExplorationOperationalWellCosts)
        setDevelopmentOperationalWellCosts(originalDevelopmentOperationalWellCosts)

        // Revert the wells to their original state
        setWellProjectWells([...originalWellProjectWells])
        setExplorationWells([...originalExplorationWells])

        // Close the modal in all cases
        setTechnicalModalIsOpen(false)
        setEditTechnicalInput(undefined)
        setActiveTab(0)
    }

    useEffect(() => {
        if (technicalModalIsOpen) {
            setExplorationOperationalWellCosts(structuredClone(project?.explorationOperationalWellCosts))
            setDevelopmentOperationalWellCosts(structuredClone(project?.developmentOperationalWellCosts))
            setOriginalExplorationOperationalWellCosts(structuredClone(project?.explorationOperationalWellCosts))
            setOriginalDevelopmentOperationalWellCosts(structuredClone(project?.developmentOperationalWellCosts))
            setWellProjectWells(structuredClone(project?.wells?.filter((w) => !isExplorationWell(w)) ?? []))
            setExplorationWells(structuredClone(project?.wells?.filter((w) => isExplorationWell(w)) ?? []))
            setOriginalWellProjectWells(structuredClone(project?.wells?.filter((w) => !isExplorationWell(w)) ?? []))
            setOriginalExplorationWells(structuredClone(project?.wells?.filter((w) => isExplorationWell(w)) ?? []))
        }
    }, [technicalModalIsOpen])

    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") {
                setTechnicalModalIsOpen(false)
            }
        }

        if (technicalModalIsOpen) {
            window.addEventListener("keydown", handleKeyDown)
        }

        return () => {
            window.removeEventListener("keydown", handleKeyDown)
        }
    }, [technicalModalIsOpen, setTechnicalModalIsOpen])

    if (!explorationOperationalWellCosts || !developmentOperationalWellCosts) { return null }

    return (
        <div>
            <Grid container>
                <Grid item xs={12}>
                    <Tabs activeTab={activeTab} onChange={setActiveTab}>
                        <List>
                            <Tab>Well Costs</Tab>
                            <Tab>PROSP</Tab>
                            <Tab>CO2</Tab>
                        </List>
                        <Panels>
                            <Panel>
                                <WellCostsTab
                                    developmentOperationalWellCosts={developmentOperationalWellCosts}
                                    setDevelopmentOperationalWellCosts={setDevelopmentOperationalWellCosts}
                                    explorationOperationalWellCosts={explorationOperationalWellCosts}
                                    setExplorationOperationalWellCosts={setExplorationOperationalWellCosts}
                                    explorationWells={explorationWells}
                                    setExplorationWells={setExplorationWells}
                                    wellProjectWells={wellProjectWells}
                                    setWellProjectWells={setWellProjectWells}
                                    setDeletedWells={setDeletedWells}
                                />
                            </Panel>
                            <Panel>
                                <PROSPTab />
                            </Panel>
                            <Panel>
                                <CO2Tab />
                            </Panel>
                        </Panels>
                    </Tabs>
                </Grid>
            </Grid>

            <Grid container justifyContent="flex-end">
                <Grid item>
                    <Button
                        type="button"
                        variant="outlined"
                        onClick={handleCancel}
                    >
                        {(editMode || editTechnicalInput) ? "Cancel" : "Close"}
                    </Button>
                </Grid>
                {(editMode || editTechnicalInput) && !isSaving && (
                    <>
                        <Grid item>
                            <Button onClick={handleSave}>Save</Button>
                        </Grid>
                        <Grid item>
                            <Button onClick={handleSaveAndClose}>Save and Close</Button>
                        </Grid>
                    </>
                )}
                {(editMode || editTechnicalInput) && isSaving && (
                    <Grid item>
                        <Button>
                            <Progress.Dots />
                        </Button>
                    </Grid>
                )}
                {!(editMode || editTechnicalInput) && (
                    <Grid item>
                        <Button onClick={() => setEditTechnicalInput(true)}>Edit</Button>
                    </Grid>
                )}
            </Grid>
        </div>

    )
}

export default EditTechnicalInputModal
